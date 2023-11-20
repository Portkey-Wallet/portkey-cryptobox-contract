using AElf.Sdk.CSharp.State;

namespace Portkey.Contracts.RedPacket
{
    /// <summary>
    /// The state class of the contract, it inherits from the AElf.Sdk.CSharp.State.ContractState type. 
    /// </summary>
    public partial class RedPacketContractState : ContractState
    {
        // state definitions go here.
        public MappedState<string,RedPacketInfo> RedPacketInfoMap { get; set; }

    }
}