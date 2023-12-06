using System.Collections.Generic;
using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace Portkey.Contracts.RedPacket
{
    /// <summary>
    /// The state class of the contract, it inherits from the AElf.Sdk.CSharp.State.ContractState type. 
    /// </summary>
    public partial class RedPacketContractState : ContractState
    {
        // state definitions go here.
        public MappedState<string,RedPacketInfo> RedPacketInfoMap { get; set; }
        
        public SingletonState<Address> Admin { get; set; }
        
        public SingletonState<bool> Initialized { get; set; }
        
        public MappedState<string,AddressList> AlreadySnatchedList{ get; set; }
        
        public SingletonState<long> RedPacketMaxCount{ get; set; }


    }
}