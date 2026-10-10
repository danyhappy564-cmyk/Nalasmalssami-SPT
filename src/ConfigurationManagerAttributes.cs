// BepInEx ConfigurationManager(F12 설정 창)가 이름으로 읽어 가는 속성 모음.
// 필드 이름이 바뀌면 F12 창이 무시하므로 이름을 바꾸지 말 것.
#pragma warning disable 0169, 0414, 0649
internal sealed class ConfigurationManagerAttributes
{
    public bool? ShowRangeAsPercent;
    public System.Action<BepInEx.Configuration.ConfigEntryBase> CustomDrawer;
    public bool? Browsable;
    public string Category;
    public object DefaultValue;
    public bool? HideDefaultButton;
    public bool? HideSettingName;
    public string Description;
    public string DispName;
    public int? Order;
    public bool? ReadOnly;
    public bool? IsAdvanced;
    public System.Func<object, string> ObjToStr;
    public System.Func<string, object> StrToObj;
}
