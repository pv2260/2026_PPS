using System;
using System.Collections.Generic;

namespace HitOrMiss
{
    /// <summary>
    /// Aggregates multiple <see cref="IResponseInputSource"/> implementations into one.
    /// Any inner source firing <see cref="ResponseReceived"/> is forwarded as-is.
    /// Enable/Disable cascade to every wrapped source so the task manager can
    /// keep its existing single-source contract while the participant has
    /// keyboard, hand pinch, and controller all live at the same time.
    /// </summary>
    public class CompositeInputSource : IResponseInputSource
    {
        public event Action<ResponseEvent> ResponseReceived;

        readonly List<IResponseInputSource> m_Sources = new();

        public IReadOnlyList<IResponseInputSource> Sources => m_Sources;

        public CompositeInputSource(params IResponseInputSource[] sources)
        {
            if (sources == null) return;
            foreach (var src in sources) Add(src);
        }

        public void Add(IResponseInputSource source)
        {
            if (source == null || m_Sources.Contains(source)) return;
            m_Sources.Add(source);
            source.ResponseReceived += Forward;
        }

        public void Remove(IResponseInputSource source)
        {
            if (source == null) return;
            if (m_Sources.Remove(source))
                source.ResponseReceived -= Forward;
        }

        public void Enable()
        {
            for (int i = 0; i < m_Sources.Count; i++) m_Sources[i].Enable();
        }

        public void Disable()
        {
            for (int i = 0; i < m_Sources.Count; i++) m_Sources[i].Disable();
        }

        void Forward(ResponseEvent ev) => ResponseReceived?.Invoke(ev);
    }
}
