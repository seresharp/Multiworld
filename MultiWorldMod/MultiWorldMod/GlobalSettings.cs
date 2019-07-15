using SeanprCore;

namespace MultiWorldMod
{
    public class GlobalSettings : BaseSettings
    {
        public string IP
        {
            get => GetString("127.0.0.1");
            set => SetString(value);
        }

        public int Port
        {
            get => GetInt(38281);
            set => SetInt(value);
        }

        public string UserName
        {
            get => GetString("Lazy_Person");
            set => SetString(value);
        }
    }
}
