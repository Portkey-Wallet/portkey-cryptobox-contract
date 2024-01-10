using AElf.Contracts.Configuration;
using AElf.Contracts.MultiToken;
using AElf.Standards.ACS0;

namespace Portkey.Contracts.CryptoBox
{
    /// <summary>
    /// The state class of the contract, it inherits from the AElf.Sdk.CSharp.State.ContractState type. 
    /// </summary>
    public partial class CryptoBoxContractState
    {
        // state definitions go here.
        internal ACS0Container.ACS0ReferenceState GenesisContract { get; set; }
        internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
        
        internal ConfigurationContainer.ConfigurationReferenceState ConfigurationContract { get; set; }
        
        internal TokenContractImplContainer.TokenContractImplReferenceState TokenContractImpl { get; set; }
    }
}