using Newtonsoft.Json;

namespace DockedVehicleStorageAccess
{
	[JsonObject]
	internal class Config
	{
		public int Postions { get; set; } = 2;
        public bool Showlogs { get; set; } = false;
        public int LockerWidth { get; set; } = 6;
		public int LockerHeight { get; set; } = 8;
		public float CheckVehiclesInterval { get; set; } = 2.0f;
		public float ExtractInterval { get; set; } = 0.25f;
		public float AutosortTransferInterval { get; set; } = 0.25f;
		public bool EasyBuild {  get; set; } = false;
		public bool UnlockedAtStart { get; set; } = false;

        [JsonIgnore]
		internal bool UseAutosortMod { get; set; }
	}
}
