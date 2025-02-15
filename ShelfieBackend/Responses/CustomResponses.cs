namespace ShelfieBackend.Responses
{
	public class CustomResponses
	{
		public record BaseResponse(bool Flag = false, string Message = null!);
		public record RegisterResponse(bool Flag = false, string Message = null!) : BaseResponse(Flag, Message);
		public record LoginResponse(bool Flag = false, string Message = null!, string JWTToken = null!) : BaseResponse(Flag, Message);
		public record GenericResponse<T>(bool Flag = false, string Message = null!, T? Data = default) : BaseResponse(Flag, Message);
	}
}
