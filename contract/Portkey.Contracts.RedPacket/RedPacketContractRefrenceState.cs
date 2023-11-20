using AElf.Contracts.MultiToken;

namespace Portkey.Contracts.RedPacket
{
    /// <summary>
    /// The state class of the contract, it inherits from the AElf.Sdk.CSharp.State.ContractState type. 
    /// </summary>
    public partial class RedPacketContractState
    {
        // state definitions go here.
        internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }

        

    }
}