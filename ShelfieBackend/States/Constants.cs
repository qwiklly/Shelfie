namespace ShelfieBackend.States
{
	public static class Constants
	{
		public const string CustomType = "Custom";
		public const string AuthCookieName = "AuthToken";

		// Predefined categories seeded by the initial migration.
		public const int ProductsCategoryId = 1;
		public const int MedicationsCategoryId = 2;

		// History item types / change types
		public const string ItemTypeProduct = "Product";
		public const string ItemTypeMedicament = "Medicament";
		public const string ItemTypeCustom = "Custom";
		public const string ChangeAdded = "Added";
		public const string ChangeUpdated = "Updated";
		public const string ChangeRemoved = "Removed";
	}
}
