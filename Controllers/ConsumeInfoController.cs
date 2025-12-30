using Azure;
using LuoliCommon.DTO.ConsumeInfo;
using LuoliCommon.DTO.Coupon;
using LuoliCommon.Entities;
using LuoliCommon.Enums;
using LuoliCommon.Interfaces;
using MethodTimer;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.ServiceModel.Channels;
using System.Text.Json;

namespace ConsumeInfoService.Controllers
{


    public class ConsumeInfoController : Controller, IConsumeInfoService
    {
        private readonly IConsumeInfoRepo _service;
        private readonly ICouponService _couponService;
        private readonly LuoliCommon.Logger.ILogger _logger;
        public ConsumeInfoController(IConsumeInfoRepo service, ICouponService couponRepository, LuoliCommon.Logger.ILogger logger)
        {
            _service = service;
            _logger = logger;
            _couponService = couponRepository;
        }

        [Time]
        [Route("api/consume-info/insert")]
        [HttpPost]
        public async Task<ApiResponse<bool>> InsertAsync([FromBody] ConsumeInfoDTO consumeInfo)
        {
            _logger.Info($"trigger ConsumeInfoService.Controllers.Insert");

            ApiResponse<bool> response = new();
            response.code = EResponseCode.Fail;
            response.data = false;

            if (!ModelState.IsValid)
            {
                response.msg = "while ConsumeInfoService.Controllers.Insert, validate failed";
                return response;
            }

            return await _service.InsertAsync(consumeInfo);
        }


        [Time]
        [Route("api/consume-info/query-id")]
        [HttpGet]
        public async Task<ApiResponse<ConsumeInfoDTO>> GetAsync([FromQuery] string goodsType, [FromQuery] long id)
        {
            goodsType= goodsType.ToLower();
            var consumeInfo = await _service.GetAsync(goodsType, id); 

            return consumeInfo;
        }

        [Time]
        [Route("api/consume-info/query-coupon")]
        [HttpGet]
        public async Task<ApiResponse<ConsumeInfoDTO>> GetAsync([FromQuery] string goodsType, [FromQuery] string coupon)
        {
            goodsType = goodsType.ToLower();
            var consumeInfo = await _service.GetAsync(goodsType, coupon);

            return consumeInfo;
        }

        [Time]
        [HttpPost]
        [Route("api/consume-info/update")]
        public async Task<ApiResponse<bool>> UpdateAsync([FromBody] LuoliCommon.DTO.ConsumeInfo.UpdateRequest ur)
        {
            ApiResponse<bool> response = new();
            response.code = EResponseCode.Fail;
            response.data = false;

            var rawStatus = ur.CI.Status;

            var updateStatus = ur.UpdateStatus(ur.CI, ur.Event);
            if (!updateStatus)
            {
                response.msg = $"for ConsumeInfo Update, coupon:[{ur.CI.Coupon}] raw Status:[{rawStatus}] Event:[{ur.Event.ToString()}], not meet UpdateStatus condition";
                _logger.Error(response.msg);
                return response;
            }

            _logger.Info($"for ConsumeInfo Update, coupon:[{ur.CI.Coupon}] raw Status:[{rawStatus.ToString()}] Event:[{ur.Event.ToString()}] new Status:[{ur.CI.Status.ToString()}]");


            var resp = await _service.UpdateAsync(ur.CI);

            return resp;
        }


        [Time]
        [HttpPost]
        [Route("api/consume-info/delete")]
        public async Task<ApiResponse<bool>> DeleteAsync([FromBody] DeleteRequest dr)
        {
            dr.GoodsType = dr.GoodsType.ToLower();
            var resp = await _service.DeleteAsync(dr.GoodsType, dr.Id);

            return resp;
        }
    }
}
