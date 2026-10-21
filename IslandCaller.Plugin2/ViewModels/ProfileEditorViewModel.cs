using Avalonia.Threading;
using ClassIsland.Shared;
using CommunityToolkit.Mvvm.Input;
using IslandCaller.Models;
using IslandCaller.Services;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using static IslandCaller.Services.ProfileService;

namespace IslandCaller.ViewModels;

public class ProfileEditorViewModel : ReactiveObject
{
    public Guid ProfileId { get; }

    public string ProfileName
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public ObservableCollection<StudentModel> ProfileList
    {
        get => field;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = new();

    public ICommand RowCommand { get; }

    private readonly RemotePolicyService _policy;

    /// <summary>RemoteCI 公平模式生效时手动权重固定为 1，不允许编辑。</summary>
    public bool IsFairModeActive => _policy.IsFairModeActive;

    public bool IsWeightEditable => !IsFairModeActive;

    /// <summary>设置页被 RemoteCI 锁定时编辑器只读，修改不会被保存。</summary>
    public bool IsSettingsLocked => _policy.IsSettingsLocked;

    private const double BaseGridHeight = 470;
    private const double NoticeBarHeight = 60;

    /// <summary>管控提示条占用窗口高度时相应压缩名单表格，保证底部按钮在默认窗口内完整可见。</summary>
    public double GridHeight =>
        BaseGridHeight - NoticeBarHeight * ((IsSettingsLocked ? 1 : 0) + (IsFairModeActive ? 1 : 0));

    public ProfileEditorViewModel(Guid profileId)
    {
        ProfileId = profileId;
        _policy = IAppHost.GetService<RemotePolicyService>();
        if (!Settings.Instance.Profile.ProfileList.TryGetValue(profileId, out var profileName))
        {
            throw new InvalidOperationException($"找不到档案 {profileId} 的名称配置。");
        }

        var profileService = IAppHost.GetService<ProfileService>();
        ProfileName = profileName;
        ProfileList = new ObservableCollection<StudentModel>(profileService.GetMembers(profileId)
            .OrderBy(m => m.Id)
            .Select(ToStudentModel));

        RowCommand = new RelayCommand<StudentModel>(RemoveStudent);
        _policy.PropertyChanged += PolicyOnPropertyChanged;
    }

    /// <summary>编辑器关闭时调用，解除对管控策略的订阅。</summary>
    public void Detach()
    {
        _policy.PropertyChanged -= PolicyOnPropertyChanged;
    }

    private void PolicyOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(RemotePolicyService.IsFairModeActive) or nameof(RemotePolicyService.IsSettingsLocked)))
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            this.RaisePropertyChanged(nameof(IsFairModeActive));
            this.RaisePropertyChanged(nameof(IsWeightEditable));
            this.RaisePropertyChanged(nameof(IsSettingsLocked));
            this.RaisePropertyChanged(nameof(GridHeight));
        });
    }

    public void AddStudent()
    {
        int nextId = ProfileList.Any() ? ProfileList.Max(s => s.ID) + 1 : 1;
        ProfileList.Add(new StudentModel
        {
            ID = nextId,
            Name = string.Empty,
            Gender = 0,
            ManualWeight = 1.0
        });
    }

    public void ReplaceMembers(IEnumerable<Person> members)
    {
        ProfileList = new ObservableCollection<StudentModel>(members
            .OrderBy(m => m.Id)
            .Select(ToStudentModel));
    }

    public List<Person> ToMembers()
    {
        return ProfileList.Select(s => new Person
        {
            Id = s.ID,
            Name = s.Name,
            Gender = s.Gender,
            ManualWeight = IsFairModeActive ? 1.0 : s.ManualWeight
        }).ToList();
    }

    private void RemoveStudent(StudentModel? student)
    {
        if (student is not null)
        {
            ProfileList.Remove(student);
        }
    }

    private static StudentModel ToStudentModel(Person member)
    {
        return new StudentModel
        {
            ID = member.Id,
            Name = member.Name,
            Gender = member.Gender,
            ManualWeight = member.ManualWeight
        };
    }

    public class StudentModel : ReactiveObject
    {
        public int ID
        {
            get => field;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        public string Name
        {
            get => field;
            set => this.RaiseAndSetIfChanged(ref field, value);
        } = string.Empty;

        public int Gender
        {
            get => field;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        public double ManualWeight
        {
            get => field;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }
    }
}
