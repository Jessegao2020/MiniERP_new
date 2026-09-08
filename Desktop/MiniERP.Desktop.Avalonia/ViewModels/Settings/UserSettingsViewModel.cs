using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;

namespace MiniERP.Desktop.ViewModels.Settings;

public sealed class UserSettingsViewModel : INotifyPropertyChanged
{
    private User? _selectedUser;
    private int _editingId;
    private string _codeText = string.Empty;
    private string _name = string.Empty;
    private string _email = string.Empty;
    private string _phone = string.Empty;
    private string _status = string.Empty;

    public ObservableCollection<User> Users { get; } = new();

    public User? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (ReferenceEquals(_selectedUser, value)) return;
            _selectedUser = value;
            OnPropertyChanged();
            if (value is not null)
                BeginEdit(value);
        }
    }

    public string CodeText
    {
        get => _codeText;
        set { if (_codeText != value) { _codeText = value; OnPropertyChanged(); } }
    }

    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; OnPropertyChanged(); } }
    }

    public string Email
    {
        get => _email;
        set { if (_email != value) { _email = value; OnPropertyChanged(); } }
    }

    public string Phone
    {
        get => _phone;
        set { if (_phone != value) { _phone = value; OnPropertyChanged(); } }
    }

    public string Status
    {
        get => _status;
        private set { if (_status != value) { _status = value; OnPropertyChanged(); } }
    }

    public bool IsExistingUser => _editingId > 0;

    public async Task LoadAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IUserService>();
            var rows = await service.GetAllUsersAsync();

            Users.Clear();
            foreach (var user in rows)
                Users.Add(user);

            if (_editingId > 0)
            {
                var current = Users.FirstOrDefault(user => user.Id == _editingId);
                if (current is not null)
                    SelectedUser = current;
            }

            Status = $"{Users.Count} user(s)";
        }
        catch (Exception ex)
        {
            Status = $"Load failed: {ex.Message}";
        }
    }

    public void NewUser()
    {
        SelectedUser = null;
        _editingId = 0;
        CodeText = string.Empty;
        Name = string.Empty;
        Email = string.Empty;
        Phone = string.Empty;
        OnPropertyChanged(nameof(IsExistingUser));
        Status = "New user.";
    }

    public async Task<bool> SaveAsync()
    {
        if (!int.TryParse(CodeText, out var code) || code < 0)
        {
            Status = "Code must be a non-negative integer.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            Status = "Name is required.";
            return false;
        }

        var user = new User
        {
            Id = _editingId,
            Code = code,
            Name = Name.Trim(),
            Email = Email.Trim(),
            Phone = Phone.Trim()
        };

        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IUserService>();

            if (_editingId == 0)
                await service.CreateUserAsync(user);
            else
                await service.UpdateUserAsync(user);

            _editingId = user.Id;
            await LoadAsync();
            Status = "Saved.";
            OnPropertyChanged(nameof(IsExistingUser));
            return true;
        }
        catch (Exception ex)
        {
            Status = $"Save failed: {ex.Message}";
            return false;
        }
    }

    public async Task<bool> DeleteAsync()
    {
        if (_editingId == 0)
        {
            NewUser();
            return true;
        }

        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IUserService>();
            await service.DeleteUserAsync(_editingId);
            NewUser();
            await LoadAsync();
            Status = "Deleted.";
            return true;
        }
        catch (Exception ex)
        {
            Status = $"Delete failed: {ex.Message}";
            return false;
        }
    }

    private void BeginEdit(User user)
    {
        _editingId = user.Id;
        CodeText = user.Code.ToString();
        Name = user.Name;
        Email = user.Email;
        Phone = user.Phone;
        OnPropertyChanged(nameof(IsExistingUser));
        Status = $"Editing {user.Name}.";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
