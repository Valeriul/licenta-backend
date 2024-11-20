using System;

namespace BackendAPI.Models
{
    public class GridPositionUpdateRequest
    {
        public ulong id_user { get; set; }
        public List<GridPositionPeripheral> Peripherals { get; set; }
    }

    public class GridPositionPeripheral
    {
        public string Uuid { get; set; }
        public uint Grid_position { get; set; }
    }
}