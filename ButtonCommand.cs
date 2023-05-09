using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageConverterPlus
{
    public class ButtonCommand : CommandBase
    {
        public ButtonCommand(Action<object?> executeCallback) : base(executeCallback)
        {

        }
    }
}
