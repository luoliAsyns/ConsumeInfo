using LuoliCommon.DTO.ConsumeInfo;
using LuoliCommon.DTO.Coupon;
using LuoliCommon.DTO.ExternalOrder;
using LuoliCommon.Entities;
using Refit;
using DeleteRequest = LuoliCommon.DTO.ConsumeInfo.DeleteRequest;
using UpdateRequest = LuoliCommon.DTO.ConsumeInfo.UpdateRequest;


namespace ConsumeInfoService
{
    public interface IConsumeInfoRepo
    {
       
        Task<LuoliCommon.Entities.ApiResponse<ConsumeInfoDTO>> GetAsync(string goodsType, long id);
        Task<LuoliCommon.Entities.ApiResponse<ConsumeInfoDTO>> GetAsync(string goodsType, string coupon);
        Task<LuoliCommon.Entities.ApiResponse<bool>> UpdateAsync(ConsumeInfoDTO dto);
        Task<LuoliCommon.Entities.ApiResponse<bool>> DeleteAsync(string goodsType, int id);
        Task<LuoliCommon.Entities.ApiResponse<bool>> InsertAsync(ConsumeInfoDTO dto);

    }
}
