using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SysWidget.ViewModels;

/// <summary>
/// Minimal hand-written MVVM base (no framework). Provides <see cref="SetValue{T}"/>
/// which assigns a backing field and raises <see cref="PropertyChanged"/> only when
/// the value actually changed.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Assigns <paramref name="value"/> to <paramref name="field"/> when different and
    /// raises <see cref="PropertyChanged"/>. Returns true when a change occurred.
    /// </summary>
    protected bool SetValue<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
