using ShelfieBackend.States;

namespace ShelfieBackend.DTOs
{
	public record CustomUserClaims(string Id = null!, string Name = null!, string Email = null!, UserRole Role = UserRole.User);
}
