using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MystTiq.Desktop.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    protected void RaisePropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
