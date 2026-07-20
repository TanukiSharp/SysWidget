namespace SysWidget.ViewModels;

/// <summary>
/// A catalog entry as shown in the tray/context menu: its name and whether it is currently
/// active. Toggling <see cref="IsActive"/> notifies the owner, which rebuilds the widget.
/// </summary>
public sealed class ComponentToggleViewModel : ViewModelBase
{
    private readonly Action<ComponentToggleViewModel> _onToggled;
    private bool _isActive;

    public ComponentToggleViewModel(string id, string displayName, bool isActive, Action<ComponentToggleViewModel> onToggled)
    {
        Id = id;
        DisplayName = displayName;
        _isActive = isActive;
        _onToggled = onToggled;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public bool IsActive
    {
        get { return _isActive; }
        set
        {
            if (SetValue(ref _isActive, value))
            {
                _onToggled(this);
            }
        }
    }
}
