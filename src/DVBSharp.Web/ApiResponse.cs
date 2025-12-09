namespace DVBSharp.Web;

public record ApiResponse<T>(bool Success, T? Data = default, string? Error = null)
{
    public static ApiResponse<T> Ok(T data) => new(true, data, null);
    public static ApiResponse<T> Fail(string error) => new(false, default, error);
}

public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data) => ApiResponse<T>.Ok(data);
    public static ApiResponse<T> Fail<T>(string error) => ApiResponse<T>.Fail(error);
}

public static class ApiResponseExtensions
{
    public static IResult ToHttpResult<T>(this ApiResponse<T> response) =>
        response.Success ? Results.Ok(response) : Results.BadRequest(response);
}
