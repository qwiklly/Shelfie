using ShelfieBackend.States;

namespace ShelfieBackend.DTOs
{
	public record CustomUserClaims(string Name = null!, string Email = null!, UserRole Role = UserRole.User);
}
