namespace ShelfieBackend.Models
{
	public class Medication
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public string Dosage { get; set; } = string.Empty;
		public DateTime ExpirationDate { get; set; }
		public int Quantity { get; set; }
		public int UserId { get; set; }
		public ApplicationUser User { get; set; } = null!;
	}
}
