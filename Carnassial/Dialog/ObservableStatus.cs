using Carnassial.Images;

namespace Carnassial.Dialog
{
    internal class ObservableStatus<TResult> : FileIOComputeTransactionStatus
    {
        public ObservableArray<TResult>? FeedbackRows { get; set; }

        public ObservableStatus()
        {
            this.FeedbackRows = null;
        }
    }
}
