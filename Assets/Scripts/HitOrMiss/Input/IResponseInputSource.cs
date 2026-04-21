using System;

namespace HitOrMiss
{
    /// <summary>
    /// Abstraction for player response input. Can be controller buttons, keyboard, or voice.
    /// </summary>
    public interface IResponseInputSource
    {
        event Action<ResponseEvent> ResponseReceived;
        void Enable();
        void Disable();
    }
}
