using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClubManagementApi.DTO
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }
        public int? TotalCount { get; set; } 
        public object Errors { get; set; }

        public static ApiResponse<T> SuccessResponse(T data, string message = "Thành công", int? totalCount = null)
            => new() { Success = true, Message = message, Data = data, TotalCount = totalCount };

        public static ApiResponse<T> FailResponse(string message, object errors = null)
            => new() { Success = false, Message = message, Errors = errors };
    }
}
