using ConsumeInfoService;
using LuoliCommon.DTO.ConsumeInfo;
using LuoliCommon.DTO.Coupon;
using LuoliCommon.DTO.ExternalOrder;
using LuoliUtils;
using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ThirdApis;
using ThirdApis.Services.Coupon;
using ThirdApis.Services.ExternalOrder;

namespace CouponService
{
    public class ConsumerService : BackgroundService
    {
        private readonly IChannel _channel;
        private readonly IServiceProvider _serviceProvider;
        private readonly string _queueName = Program.Config.KVPairs["StartWith"]+ RabbitMQKeys.ConsumeInfoInserting; // 替换为你的队列名
        private readonly LuoliCommon.Logger.ILogger _logger;

        private readonly IExternalOrderRepository _externalOrderRepository;
        private readonly ICouponRepository _couponRepository;

        public ConsumerService(IChannel channel,
             IServiceProvider serviceProvider,
             LuoliCommon.Logger.ILogger logger,
             IExternalOrderRepository externalOrderRepository,
             ICouponRepository  couponRepository
             )
        {
            _channel = channel;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _externalOrderRepository = externalOrderRepository;
            _couponRepository = couponRepository;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 声明队列
            await _channel.QueueDeclareAsync(
                queue: _queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: stoppingToken);

            // 设置Qos
            await _channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: 10,
                global: false,
                stoppingToken);

            // 创建消费者
            var consumer = new AsyncEventingBasicConsumer(_channel);

            // 处理接收到的消息
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                try
                {
                    _logger.Info("ConsumeInfo.ConsumerService[For insert ConsumeInfoDTO into DB] received message");
                    _logger.Debug(message);

                    var dto = JsonSerializer.Deserialize<ConsumeInfoDTO>(message);
                    // 使用ServiceProvider创建作用域，以便获取Controller实例
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        // 获取你的Controller实例
                        IConsumeInfoService cis = scope.ServiceProvider.GetRequiredService<IConsumeInfoService>();

                        var resp = await cis.InsertAsync(dto);

                        if (resp.ok)
                        {
                            _logger.Info($"ConsumeInfo.ConsumerService[For insert ConsumeInfoDTO into DB] success with ConsumeInfoDTO.Coupon[{dto.Coupon}]");

                            // 处理成功，确认消息
                            await _channel.BasicAckAsync(
                                deliveryTag: ea.DeliveryTag,
                                multiple: false,
                                stoppingToken);
                        }
                        else
                        {
                            // 处理失败，不重新入队
                            _logger.Error($"ConsumeInfo.ConsumerService[For insert ConsumeInfoDTO into DB] failed with ConsumeInfoDTO.Coupon[{dto.Coupon}]");
                            _logger.Error(resp.msg);
                            Notify(dto, $"ConsumeInfo.ConsumerService ConsumeInfoDTO insert failed, msg:[{resp.msg}]", ea.DeliveryTag, stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("while ConsumerService consuming");
                    _logger.Error(ex.Message);
                    // 处理异常，记录日志
                    // 异常情况下不确认消息，不重新入队
                    await _channel.BasicNackAsync(
                        deliveryTag: ea.DeliveryTag,
                        multiple: false,
                        requeue: false,
                        stoppingToken);

                    ApiCaller.NotifyAsync(
@$"{Program.Config.ServiceName}.{Program.Config.ServiceId}
MQ 消费过程中异常

message:[{message}]", Program.NotifyUsers);
                }
            };

            _logger.Info($"ConsumeInfo.ConsumerService start listen MQ[{_queueName}]");

            // 开始消费
            await _channel.BasicConsumeAsync(
                queue: _queueName,
                autoAck: false,
                consumerTag: Program.Config.ServiceName,
                noLocal: false,
                exclusive: false,
                arguments: null,
                consumer: consumer,
                stoppingToken);

            // 保持服务运行直到应用程序停止
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        /// <summary>
        /// ConsumeFailed  统一处理
        /// </summary>
        /// <param name="coupon"></param>
        /// <param name="externalOrder"></param>
        /// <param name="coreMsg"></param>
        private async Task Notify(ConsumeInfoDTO ci, string coreMsg, ulong tag, CancellationToken token)
        {
            try
            {
                CouponDTO coupon = (await _couponRepository.Query(ci.Coupon)).data;
                ExternalOrderDTO externalOrder = (await _externalOrderRepository.Get(coupon.ExternalOrderFromPlatform, coupon.ExternalOrderTid)).data;

                _channel.BasicNackAsync(
                          deliveryTag: tag,
                          multiple: false,
                          requeue: false,
                          token);


                Program.Notify(
                    coupon,
                    externalOrder,
                    coreMsg);
            }
            catch (Exception ex)
            {
                _logger.Error("while Notify");
                _logger.Error(ex.Message);
            }
        }

    }
}
