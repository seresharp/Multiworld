using SeanprCore;

namespace MultiWorldMod
{
    public class SaveSettings : BaseSettings
    {
        public bool SlyCharm
        {
            get => GetBool(false);
            set => SetBool(value);
        }
    }
}
