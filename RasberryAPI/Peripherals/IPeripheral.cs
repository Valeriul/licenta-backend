namespace RasberryAPI.Peripherals
{
    public interface IPeripheral
    {
        string UUId { get; set; }
        string Url { get; set; }

        int BatteryLevel { get; set; }
        void HandleRequest(string requestData);
        string GetData();
    }
}
