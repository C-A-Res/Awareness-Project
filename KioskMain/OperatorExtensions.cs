using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Psi;

namespace NU.Kiosk
{
    public static class OperatorExtensions
    {
        /// <summary>
        /// Defines an operator that will hold a signal (i.e. smooth out the signal)
        /// </summary>
        /// <param name="source">Source of the signal</param>
        /// <param name="threshold">Threshold below which the signal is considered off</param>
        /// <param name="decay">Speed at which signal decays</param>
        /// <returns>Returns true if signal is on, false otherwise</returns>
        public static IProducer<bool> Hold(this IProducer<double> source, double threshold, double decay = 0.2)
        {
            double maxValue = 0;

            return source.Select(
                newValue =>
                {
                    if (newValue > maxValue && newValue > threshold)
                    {
                        maxValue = newValue;
                    }
                    else
                    {
                        maxValue = maxValue * (1 - decay) + newValue * decay;
                    }

                    return maxValue >= threshold;
                });
        }
    }
}
