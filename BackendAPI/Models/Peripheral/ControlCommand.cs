using System;

namespace BackendAPI.Models
{

    public class ControlCommand
    {
        public string CommandType { get; } = "control";
        public string Data { get; set; } = string.Empty;
        public string Uuid { get; set; } = string.Empty;
    }

}