using Microsoft.Psi;
using Microsoft.Psi.Audio;
using Microsoft.Psi.Components;
using Microsoft.Psi.Speech;
using NU.Kiosk.Speech;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;

namespace NU.Kiosk.Speech
{
    public class IntervalBooleanProducer : IProducer<bool>, IStartable
    {
        public Emitter<bool> Out { get; private set; }

        private System.Timers.Timer s_timer;
        private int position;
        private int counter;
        private int[] pattern;

        private bool state;

        public IntervalBooleanProducer(Pipeline pipeline, int[] pattern = null, int tick_size = 50) // 0.05 interval
        {
            this.Out = pipeline.CreateEmitter<bool>(this, nameof(IntervalBooleanProducer));
            this.s_timer = new System.Timers.Timer(tick_size);
            
            if (pattern == null || pattern.Length == 0)
            {
                this.pattern = new int[] { 200, 100 }; // 10 seconds present, 5 seconds away
            } else
            {
                this.pattern = pattern;
            }            
        }

        private void OnSessionTimedEvent(object source, ElapsedEventArgs e)
        {
            if (++counter >= pattern[position])
            {
                counter = 0;
                position = (position + 1) % pattern.Length;
                state = position % 2 == 0;
                Out.Post(state, DateTime.Now);
            } else
            {
                counter++;
            }
            // restart the timer immediately            
            s_timer.Start();
        }

        public void Start(System.Action onCompleted, ReplayDescriptor descriptor)
        {
            position = 0;
            counter = 0;
            state = true;

            s_timer.Elapsed += this.OnSessionTimedEvent;
            Out.Post(state, DateTime.Now);
            s_timer.Start();
        }

        public void Stop()
        {
            s_timer.Stop();
        }
    }
}
