namespace MyApp.Models
{
    public class ApiResponse<T>
    {
        public bool IsSuccess { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }
        public List<string>? Errors { get; set; }

        // Quick helper constructor for Successful responses
        public static ApiResponse<T> Success(T data, string? message = "Success")
        {
            return new ApiResponse<T> { IsSuccess = true, Data = data, Message = message };
        }

        // Quick helper constructor for Failed responses
        public static ApiResponse<T> Fail(string errorMessage, List<string>? detailedErrors = null)
        {
            return new ApiResponse<T> 
            { 
                IsSuccess = false, 
                Message = errorMessage, 
                Errors = detailedErrors ?? new List<string>() 
            };
        }
    }
}