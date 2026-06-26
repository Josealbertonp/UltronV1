namespace UltronV1.Core.Dataset
{
    public interface IDatasetSink
    {
        void OpenSession(DatasetManifest manifest);
        void AppendOpportunity(OpportunityRecord record);
        void AppendOutcome(TradeOutcome outcome);
        void UpdateManifest(DatasetManifest manifest);
        void CloseSession();
    }

    public sealed class NullDatasetSink : IDatasetSink
    {
        public static readonly NullDatasetSink Instance = new NullDatasetSink();

        NullDatasetSink() { }

        public void OpenSession(DatasetManifest manifest) { }
        public void AppendOpportunity(OpportunityRecord record) { }
        public void AppendOutcome(TradeOutcome outcome) { }
        public void UpdateManifest(DatasetManifest manifest) { }
        public void CloseSession() { }
    }
}
