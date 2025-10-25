using Azure.Core;
using Dm.util;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using LuoliCommon.DTO.ConsumeInfo;
using LuoliCommon.DTO.Coupon;
using LuoliCommon.DTO.ExternalOrder;
using LuoliCommon.Entities;
using LuoliCommon.Enums;
using LuoliCommon.Logger;
using LuoliDatabase;
using LuoliDatabase.Entities;
using LuoliDatabase.Extensions;
using LuoliUtils;
using MethodTimer;
using RabbitMQ.Client;
using SqlSugar;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static Azure.Core.HttpHeader;
using Decoder = LuoliUtils.Decoder;
using ILogger = LuoliCommon.Logger.ILogger;

namespace ConsumeInfoService
{
    // 实现服务接口
    public class SqlSugarConsumeInfoService : IConsumeInfoService
    {
        // 注入的依赖项
        private readonly ILogger _logger;
        private readonly SqlSugarClient _sqlClient;
        private readonly IChannel _channel;
        
        private static  BasicProperties _rabbitMQMsgProps = new BasicProperties();

        // 构造函数注入
        public SqlSugarConsumeInfoService(ILogger logger, SqlSugarClient sqlClient, IChannel channel)
        {
            _logger = logger;
            _sqlClient = sqlClient;
            _channel = channel;

            _rabbitMQMsgProps.ContentType = "text/plain";
            _rabbitMQMsgProps.DeliveryMode = DeliveryModes.Persistent;
        }


        public async Task<ApiResponse<bool>> DeleteAsync(string goodsType, long id)
        {
            _logger.Debug($"starting SqlSugarConsumeInfoService.DeleteAsync with id:[{id}] goodsType:[{goodsType}]");

            var redisKey = $"{goodsType}.{id}";

            var result = new ApiResponse<bool>();
            result.code = EResponseCode.Fail;
            result.data = false;

            try
            {
                await _sqlClient.BeginTranAsync();
                int impactRows= await _sqlClient.Updateable<object>()
                    .AS(goodsType)
                    .SetColumns("is_deleted", true)
                    .Where($"id='{id}'").ExecuteCommandAsync();
                await _sqlClient.CommitTranAsync();

                if (impactRows != 1)
                    throw new Exception("SqlSugarConsumeInfoService.DeleteAsync impactRows not equal to 1");

                result.code = EResponseCode.Success;
                result.data = true;
                _logger.Debug($"SqlSugarConsumeInfoService.DeleteAsync success with id:[{id}] goodsType:[{goodsType}]");

                RedisHelper.DelAsync(redisKey);

            }
            catch (Exception ex)
            {
                result.msg = ex.Message;
                await _sqlClient.RollbackTranAsync();
                _logger.Error($"while SqlSugarConsumeInfoService.DeleteAsync with id:[{id}] goodsType:[{goodsType}]");
                _logger.Error(ex.Message);
            }

            return result;
        }

        public async Task<ApiResponse<ConsumeInfoDTO>> GetAsync(string goodsType, string coupon)
        {
            _logger.Debug($"starting SqlSugarConsumeInfoService.GetAsync with coupon:[{coupon}] goodsType:[{goodsType}]");
            var result = new ApiResponse<ConsumeInfoDTO>();
            result.code = EResponseCode.Fail;
            result.data = null;

            try
            {
                var redisKey = $"{goodsType}.{coupon}";
                var consumeInfoEntity = RedisHelper.Get<ConsumeInfoEntity>(redisKey);

                if(!(consumeInfoEntity is null))
                {
                    result.code = EResponseCode.Success;
                    result.data = consumeInfoEntity.ToDTO();
                    result.msg = "from redis";
                    return result;
                }

                consumeInfoEntity = await _sqlClient.Queryable<ConsumeInfoEntity>()
                    .AS(goodsType) //指定表名
                    .Where(o=>o.coupon == coupon && o.is_deleted == 0)
                    .FirstAsync();
              
               
                result.code = EResponseCode.Success;
                result.data = consumeInfoEntity.ToDTO();
                result.msg = "from database";

                _logger.Debug($"SqlSugarConsumeInfoService.GetAsync success with coupon:[{coupon}] goodsType:[{goodsType}]");
              
                if (!(result.data is null))
                    RedisHelper.SetAsync(redisKey, consumeInfoEntity, 60);

            }
            catch (Exception ex)
            {
                result.msg = ex.Message;
                _logger.Error($"while SqlSugarConsumeInfoService.GetAsync with  with coupon:[{coupon}] goodsType:[{goodsType}]");
                _logger.Error(ex.Message);
            }

            return result;
        }

        public async Task<ApiResponse<ConsumeInfoDTO>> GetAsync(string goodsType, long id)
        {
            _logger.Debug($"starting SqlSugarConsumeInfoService.GetAsync with id:[{id}] goodsType:[{goodsType}]");
            var result = new ApiResponse<ConsumeInfoDTO>();
            result.code = EResponseCode.Fail;
            result.data = null;

            var redisKey = $"{goodsType}.{id}";

            try
            {
                var consumeInfoEntity = RedisHelper.Get<ConsumeInfoEntity>(redisKey);

                if (!(consumeInfoEntity is null))
                {
                    result.code = EResponseCode.Success;
                    result.data = consumeInfoEntity.ToDTO();
                    result.msg = "from redis";
                    return result;
                }

                consumeInfoEntity = await _sqlClient.Queryable<ConsumeInfoEntity>()
                    .AS(goodsType) //指定表名
                    .Where(o=>o.id == id && o.is_deleted == 0)
                    .FirstAsync();
              
                
                result.data = consumeInfoEntity.ToDTO();
                result.code = EResponseCode.Success;
                _logger.Debug($"SqlSugarConsumeInfoService.GetAsync success with id:[{id}] goodsType:[{goodsType}]");

                if (!(result.data is null))
                    RedisHelper.SetAsync(redisKey, consumeInfoEntity, 60);

            }
            catch (Exception ex)
            {
                result.msg = ex.Message;
                _logger.Error($"while SqlSugarConsumeInfoService.GetAsync with id:[{id}] goodsType:[{goodsType}]");
                _logger.Error(ex.Message);
            }

            return result;
        }


        public async Task<ApiResponse<bool>> UpdateAsync(ConsumeInfoDTO dto)
        {
            _logger.Debug($"starting SqlSugarConsumeInfoService.UpdateAsync with id:[{dto.Id}] goodsType:[{dto.GoodsType}]");
            var result = new ApiResponse<bool>();
            result.code = EResponseCode.Fail;
            result.data = false;

            try
            {
                var redisKey = $"{dto.GoodsType}.{dto.Id}";

                await _sqlClient.BeginTranAsync();
                int impactRows = await _sqlClient.Updateable(dto.ToEntity())
                 .Where($"id='{dto.Id}'").ExecuteCommandAsync();
                await _sqlClient.CommitTranAsync();
                if (impactRows != 1)
                    throw new Exception("SqlSugarConsumeInfoService.UpdateAsync impactRows not equal to 1");

                result.code = EResponseCode.Success;
                result.data = true;

                _logger.Debug($"SqlSugarConsumeInfoService.UpdateAsync success  with id:[{dto.Id}] goodsType:[{dto.GoodsType}]");

                RedisHelper.DelAsync(redisKey);
            }
            catch (Exception ex)
            {
                result.msg = ex.Message;
                await _sqlClient.RollbackTranAsync();
                _logger.Error($"while SqlSugarConsumeInfoService.UpdateAsync  with id:[{dto.Id}] goodsType:[{dto.GoodsType}]");
                _logger.Error(ex.Message);
            }

            return result;
        }

        public async Task<ApiResponse<bool>> InsertAsync(ConsumeInfoDTO info)
        {
            string goodsType = info.GoodsType;

            _logger.Debug($"starting SqlSugarConsumeInfoService.InsertAsync with coupon:[{info.Coupon}] goodsType:[{goodsType}]");


            var result = new ApiResponse<bool>();
            result.code = EResponseCode.Fail;
            result.data = false;

            try
            {
                int impactRows = await _sqlClient.Insertable(info)
                    .AS(goodsType)
                    .ExecuteCommandAsync();

                if (impactRows != 1)
                    throw new Exception("SqlSugarConsumeInfoService.InsertAsync impactRows not equal to 1");

                result.code = EResponseCode.Success;
                result.data = true;
                _logger.Debug($"SqlSugarConsumeInfoService.InsertAsync success with coupon:[{info.Coupon}] goodsType:[{goodsType}]");

                await _channel.BasicPublishAsync(exchange: string.Empty,
               routingKey: RabbitMQKeys.ConsumeInfoInserted,
               true,
               _rabbitMQMsgProps,
              Encoding.UTF8.GetBytes(JsonSerializer.Serialize(info)));

            }
            catch (Exception ex)
            {
                result.msg = ex.Message;
                await _sqlClient.RollbackTranAsync();
                _logger.Error($"while SqlSugarConsumeInfoService.InsertAsync with coupon:[{info.Coupon}] goodsType:[{goodsType}]");
                _logger.Error(ex.Message);
            }

            return result;


        }

      
    }
}
