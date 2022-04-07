using System;
using System.Threading;

namespace Pisstaube.Utils
{
    /// <summary>
    /// a Request Limiter to prevent overflowing osu!API V2 with tons of API Requests.
    /// </summary>
    public class RequestLimiter
    {
        private readonly int _requestAmount;
        private readonly TimeSpan _howLong;
        private DateTime _dtLast;
        private DateTime _dtNext;

        private int _req;

        public RequestLimiter(int requestAmount, TimeSpan howLong)
        {
            _requestAmount = requestAmount - 1;
            _howLong = howLong;
            _dtLast = DateTime.Now;
            _dtNext = DateTime.Now;

            _dtNext += howLong;
        }

        /// <summary>
        /// Limits the current Request Count (freezes Thread until the Timeout is over)
        /// </summary>
        public void Limit()
        {
            if (_dtLast > _dtNext)
            {
                _req = 1;

                _dtNext = DateTime.Now;
                _dtNext += _howLong;
            }

            if (_req > _requestAmount && _dtNext.Subtract(_dtLast).TotalMilliseconds > 0)
                Thread.Sleep(_dtNext.Subtract(_dtLast));

            _req++;
            _dtLast = DateTime.Now;
        }
    }
}