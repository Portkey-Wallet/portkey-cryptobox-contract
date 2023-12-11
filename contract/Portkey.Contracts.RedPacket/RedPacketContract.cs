using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using System.Linq;
using AElf;

namespace Portkey.Contracts.RedPacket
{
    public partial class RedPacketContract : RedPacketContractContainer.RedPacketContractBase
    {
        public override Empty Initialize(InitializeInput input)
        {
            Assert(!State.Initialized.Value, "Already initialized.");
            Assert(input.MaxCount > 0, "MaxCount should be greater than 0.");
            State.Admin.Value = input.ContractAdmin ?? Context.Sender;
            State.RedPacketMaxCount.Value = input.MaxCount;
            State.TokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
            State.Initialized.Value = true;
            return new Empty();
        }

        public override Empty CreateRedPacket(CreateRedPacketInput input)
        {
            Assert(!string.IsNullOrEmpty(input.RedPacketId), "RedPacketId should not be null.");
            Assert(State.RedPacketInfoMap[input.RedPacketId] == null, "RedPacketId already exists.");
            Assert(input.TotalAmount > 0, "TotalAmount should be greater than 0.");
            Assert(input.TotalCount > 0, "TotalCount should be greater than 0.");
            Assert(State.RedPacketMaxCount.Value >= input.TotalCount,
                "TotalCount should be less than or equal to MaxCount.");
            Assert(input.MinAmount > 0, "MinAmount should be greater than 0.");
            Assert(
                !string.IsNullOrEmpty(input.RedPacketSymbol), "RedPacketSymbol should not be null.");
            Assert(input.TotalAmount >= input.MinAmount * input.TotalCount,
                "TotalAmount should be greater than MinAmount * TotalCount.");
            Assert(input.ExpirationTime > Context.CurrentBlockTime.Seconds, "ExpiredTime should be greater than now.");
            Assert(input.SenderAddress != null, "SenderAddress should not be null.");
            Assert(!string.IsNullOrWhiteSpace(input.RedPacketSignature), "signature should not be null");
            Assert(!string.IsNullOrEmpty(input.PublicKey), "PublicKey should not be null.");

            var maxCount = State.RedPacketMaxCount.Value;

            var message = $"{input.RedPacketId}-{input.RedPacketSymbol}-{input.MinAmount}-{maxCount}";
            var verifySignature = VerifySignature(input.PublicKey, input.RedPacketSignature, message);

            Assert(verifySignature, "Invalid signature.");
            var virtualAddress =
                Context.ConvertVirtualAddressToContractAddress(HashHelper.ComputeFrom(input.RedPacketId));
            State.TokenContract.TransferFrom.Send(new TransferFromInput
            {
                From = Context.Sender,
                To = Context.Self,
                Amount = input.TotalAmount,
                Symbol = input.RedPacketSymbol,
                Memo = "RedPacket"
            });

            State.TokenContract.Transfer.Send(new TransferInput
            {
                To = virtualAddress,
                Amount = input.TotalAmount,
                Symbol = input.RedPacketSymbol,
                Memo = "RedPacket"
            });

            var redPacket = new RedPacketInfo
            {
                RedPacketId = input.RedPacketId,
                RedPacketType = input.RedPacketType,
                RedPacketSymbol = input.RedPacketSymbol,
                TotalCount = input.TotalCount,
                TotalAmount = input.TotalAmount,
                ExpirationTime = input.ExpirationTime,
                PublicKey = input.PublicKey,
                SenderAddress = Context.Sender
            };
            State.RedPacketInfoMap[input.RedPacketId] = redPacket;
            Context.Fire(
                new RedPacketCreated
                {
                    SenderAddress = input.SenderAddress,
                    RedPacketId = input.RedPacketId,
                    RedPacketType = input.RedPacketType,
                    RedPacketSymbol = input.RedPacketSymbol,
                    TotalCount = input.TotalCount,
                    TotalAmount = input.TotalAmount,
                    ReceiverAddress = virtualAddress
                }
            );
            return new Empty();
        }


        public override Empty TransferRedPacket(TransferRedPacketBatchInput input)
        {
            var inputs = input.TransferRedPacketInputs;
            Assert(inputs != null && input.TransferRedPacketInputs.Count > 0 && input.RedPacketId != null, "Invalidate Input.");
            var redPacket = State.RedPacketInfoMap[input.RedPacketId];
            Assert(redPacket != null, "RedPacket not exists.");
            var virtualAddressHash = HashHelper.ComputeFrom(input.RedPacketId);
            var snatchedList = State.AlreadySnatchedList[input.RedPacketId]
                       ?? new AddressList();
            foreach (var transferRedPacketInput in inputs!)
            {
                Assert(!transferRedPacketInput.ReceiverAddress.Value.IsNullOrEmpty(), "ReceiverAddress is empty.");
                Assert(snatchedList.Addresses.FirstOrDefault(c => c.Value == transferRedPacketInput.ReceiverAddress.Value) == null, "ReceiverAddress " + transferRedPacketInput.ReceiverAddress + " already received.");

                var message =
                    $"{redPacket.RedPacketId}-{transferRedPacketInput.ReceiverAddress}-{transferRedPacketInput.Amount}";
                var verifySignature = VerifySignature(redPacket.PublicKey, transferRedPacketInput.RedPacketSignature,
                    message);
                Assert(verifySignature, "Invalid signature.");
                Context.SendVirtualInline(virtualAddressHash, State.TokenContract.Value,
                    nameof(State.TokenContract.Transfer),
                    new TransferInput
                    {
                        To = transferRedPacketInput.ReceiverAddress,
                        Amount = transferRedPacketInput.Amount,
                        Symbol = redPacket.RedPacketSymbol,
                        Memo = "TransferToReceiver"
                    }.ToByteString());
                Context.Fire(new RedPacketReceived
                {
                    RedPacketId = redPacket.RedPacketId,
                    ReceiverAddress = transferRedPacketInput.ReceiverAddress,
                    Amount = transferRedPacketInput.Amount,
                    SenderAddress = redPacket.SenderAddress,
                    IsSuccess = true
                });
                snatchedList.Addresses.Add(transferRedPacketInput.ReceiverAddress);
            }
            State.AlreadySnatchedList[input.RedPacketId] = snatchedList;

            return new Empty();
        }

        public override Empty RefundRedPacket(RefundRedPacketInput input)
        {
            Assert(!string.IsNullOrEmpty(input.RedPacketId), "RedPacketId should not be null.");
            var redPacket = State.RedPacketInfoMap[input.RedPacketId];
            Assert(redPacket != null, "RedPacket not exists.");
            Assert(redPacket?.ExpirationTime < Context.CurrentBlockTime.Seconds, "RedPacket not expired.");
            var virtualAddressHash = HashHelper.ComputeFrom(input.RedPacketId);
            var message =
                $"{redPacket.RedPacketId}-{input.Amount}";
            var verifySignature = VerifySignature(redPacket.PublicKey, input.RedPacketSignature,
                message);
            Assert(verifySignature, "Invalid signature.");
            Context.SendVirtualInline(virtualAddressHash, State.TokenContract.Value,
                nameof(State.TokenContract.Transfer),
                new TransferInput
                {
                    To = redPacket.SenderAddress,
                    Amount = input.Amount,
                    Symbol = redPacket.RedPacketSymbol,
                    Memo = "RefundRedPacket"
                }.ToByteString());
            Context.Fire(new RedPacketRefunded
            {
                RedPacketId = redPacket.RedPacketId,
                RefundAddress = redPacket.SenderAddress,
                Amount = input.Amount,
                RedPacketSymbol = redPacket.RedPacketSymbol
            });
            
            return new Empty();
        }

        public override RedPacketOutput GetRedPacketInfo(GetRedPacketInput input)
        {
            var packetInfo = State.RedPacketInfoMap[input.RedPacketId];
            Assert(packetInfo != null, "RedPacketId not exists.");
            return new RedPacketOutput
            {
                RedPacketInfo = packetInfo
            };
        }

        public override Empty SetRedPacketMaxCount(SetRedPacketMaxCountInput input)
        {
            Assert(Context.Sender == State.Admin.Value, "No permission.");
            Assert(input.MaxCount > 0, "MaxCount should be greater than 0.");
            State.RedPacketMaxCount.Value = input.MaxCount;
            return new Empty();
        }

        public override RedPacketMaxCountOutput GetRedPacketMaxCount(Empty input)
        {
            return new RedPacketMaxCountOutput
            {
                MaxCount = State.RedPacketMaxCount.Value
            };
        }

        private bool VerifySignature(string publicKey, string signature, string message)
        {
            var messageBytes = HashHelper.ComputeFrom(message).ToByteArray();
            var signatureBytes = ByteStringHelper.FromHexString(signature).ToByteArray();
            var recoverPublicKey = Context.RecoverPublicKey(signatureBytes, messageBytes).ToHex();
            return recoverPublicKey == publicKey;
        }
    }
}