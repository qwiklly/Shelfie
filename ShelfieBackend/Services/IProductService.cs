using ShelfieBackend.DTOs;
using ShelfieBackend.Models;
using static ShelfieBackend.Responses.CustomResponses;

namespace ShelfieBackend.Services
{
    public interface IProductService
    {
        Task<BaseResponse> GetProductsAsync(CancellationToken cancellationToken = default);
        Task<BaseResponse> GetProductAsync(string name, CancellationToken cancellationToken = default);
        Task<BaseResponse> AddProductAsync(AddProductDTO model, CancellationToken cancellationToken = default);
        Task<BaseResponse> UpdateProductAsync(string currentName, UpdateProductDTO model, CancellationToken cancellationToken = default);
        Task<BaseResponse> DeleteProductAsync(string name, CancellationToken cancellationToken = default);
        Task<List<HistoryRecord>> GetAllHistoryAsync(CancellationToken cancellationToken = default);
    }
}
