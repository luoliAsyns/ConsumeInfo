using LuoliCommon.DTO.ConsumeInfo;
using LuoliCommon.Entities;
using LuoliCommon.Enums;
using MethodTimer;
using Microsoft.AspNetCore.Mvc;
using System.ServiceModel.Channels;
using System.Text.Json;

namespace ConsumeInfoService.Controllers
{


    public class ConsumeInfoController : Controller
    {
        private readonly IConsumeInfoService _service;
        private readonly LuoliCommon.Logger.ILogger _logger;
        public ConsumeInfoController(IConsumeInfoService service, LuoliCommon.Logger.ILogger logger)
        {
            _service = service;
            _logger = logger;
        }

        [Time]
        [Route("api/consume-info/insert")]
        [HttpPost]
        public async Task<ApiResponse<bool>> Insert([FromBody] ConsumeInfoDTO consumeInfo)
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
        [Route("api/consume-info/query")]
        [HttpGet]
        public async Task<ApiResponse<ConsumeInfoDTO>> GetById([FromQuery] string goodsType, [FromQuery] long id)
        {
            var consumeInfo = await _service.GetAsync(goodsType, id); 

            return consumeInfo;
        }


        [Time]
        [HttpPost]
        [Route("api/external-order/delete")]
        public async Task<ApiResponse<bool>> Delete(string goodsType, long id)
        {
            var resp = await _service.DeleteAsync(goodsType, id);

            return resp;
        }
    }
}
