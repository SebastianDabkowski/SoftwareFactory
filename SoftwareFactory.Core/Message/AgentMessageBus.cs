using System;
using System.Collections.Generic;
using System.Text;

namespace SoftwareFactory.Core.Message
{
    public class AgentMessageBus
    {
        private readonly Dictionary<int, List<Action<string>>> _subscribers = new();



        public void Subscribe(int agentId, Action<string> callback)
        {
            if (!_subscribers.ContainsKey(agentId))
            {
                _subscribers[agentId] = new List<Action<string>>();
            }

            _subscribers[agentId].Add(callback);
        }

        public void Send(int messageType, Action<string> callback)
        {
            if (_subscribers.ContainsKey(messageType))
            {
                _subscribers[messageType].Remove(callback);
            }
        }
    }
}
