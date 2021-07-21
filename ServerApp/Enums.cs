using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerApp
{
    enum CommandsFromClient
    {
        SEND_TEXT = 1,
        SEND_BACKSPACE,
        SEND_MOVE_MOUSE,
        SEND_LEFT_MOUSE,
        SEND_RIGHT_MOUSE,
        SEND_PREVIOUS,
        SEND_PLAYSTOP,
        SEND_NEXT,
        SEND_VOLDOWN,
        SEND_STOP,
        SEND_VOLUP,
        SEND_OPEN_WEBPAGE,
        SEND_WHEEL_MOUSE,
        SEND_LEFT_MOUSE_LONG_PRESS_START,
        SEND_LEFT_MOUSE_LONG_PRESS_STOP,
        SEND_PING,
    }

    enum CommandsFromServer
    {
        SEND_PING = 1,
        SEND_PLAYBACK_INFO,
    }
}
