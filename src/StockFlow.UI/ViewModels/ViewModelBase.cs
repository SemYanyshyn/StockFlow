using CommunityToolkit.Mvvm.ComponentModel;

namespace StockFlow.UI.ViewModels
{
    public abstract class ViewModelBase : ObservableObject
    {
        protected async Task<bool> ExecuteSafelyAsync(
            Func<Task> operation,
            Action<Exception> handleError,
            Action? onStarting = null,
            Action? onFinally = null)
        {
            onStarting?.Invoke();
            try
            {
                await operation();
                return true;
            }
            catch (Exception error)
            {
                handleError(error);
                return false;
            }
            finally
            {
                onFinally?.Invoke();
            }
        }
    }
}
