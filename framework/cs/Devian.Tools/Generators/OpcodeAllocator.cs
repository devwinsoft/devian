using System.Collections.Generic;
using Devian.Tools.Models;

namespace Devian.Tools.Generators
{
    public class OpcodeAllocator
    {
        private int _next = 1;
        private readonly Dictionary<string, int> _assigned = new Dictionary<string, int>();
        
        public int Allocate(MessageSpec msg)
        {
            if (msg.Opcode.HasValue)
            {
                _assigned[msg.Name] = msg.Opcode.Value;
                if (msg.Opcode.Value >= _next)
                    _next = msg.Opcode.Value + 1;
                return msg.Opcode.Value;
            }
            
            var opcode = _next++;
            _assigned[msg.Name] = opcode;
            return opcode;
        }
        
        public int Get(string name) 
        { 
            int v;
            return _assigned.TryGetValue(name, out v) ? v : 0; 
        }
    }
}
