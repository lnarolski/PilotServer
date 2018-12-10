using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerApp
{
    enum Commands
    {
        SEND_TEXT = 1,
        SEND_BACKSPACE,
        SEND_MOVE_MOUSE,
        SEND_LEFT_MOUSE,
        SEND_RIGHT_MOUSE,
    }
}
