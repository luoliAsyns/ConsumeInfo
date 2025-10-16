using LuoliCommon.DTO.ConsumeInfo;
using LuoliCommon.DTO.Coupon;
using LuoliCommon.DTO.ExternalOrder;
using LuoliCommon.Entities;
using LuoliCommon.Enums;

namespace ConsumeInfoService
{
    public interface IConsumeInfoService
    {

        Task<ApiResponse<bool>> InsertAsync(ConsumeInfoDTO info);
        Task<ApiResponse<ConsumeInfoDTO>> GetAsync(string goodsType, string coupon);
        Task<ApiResponse<ConsumeInfoDTO>> GetAsync(string goodsType, long id);
        Task<ApiResponse<bool>> UpdateAsync(ConsumeInfoDTO info);
        Task<ApiResponse<bool>> DeleteAsync(string goodsType, long id);

    }
}
