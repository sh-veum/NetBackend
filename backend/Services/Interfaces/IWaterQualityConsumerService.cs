namespace NetBackend.Services.Interfaces;

public interface IWaterQualityConsumerService
{
    void SubscribeToTopic(string newTopic, bool sendHistoricalData = false);
}