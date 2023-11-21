using AElf;
using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace Portkey.Contracts.RedPacket
{
    public partial class RedPacketContract : RedPacketContractContainer.RedPacketContractBase
    {
        public override Empty CreateRedPacket(CreateRedPacketInput input)
        {
            Assert(input.RedPacketId != null, "RedPacketId should not be null.");
            Assert(State.RedPacketInfoMap[input.RedPacketId] == null, "RedPacketId already exists.");
            Assert(input.TotalAmount > 0, "TotalAmount should be greater than 0.");
            Assert(input.TotalCount is > 0 and < RedPacketContractConstants.MaxRedPacketCount,
                "TotalCount should be greater than 0.");
            //verify min * count < total
            Assert(input.MinAmount > 0, "MinAmount should be greater than 0.");
            Assert(input.RedPacketSymbol != null, "RedPacketSymbol should not be null.");
            Assert(input.TotalAmount > input.MinAmount * input.TotalCount,
                "TotalAmount should be greater than MinAmount * TotalCount.");
            Assert(input.ExpirationTime > Context.CurrentBlockTime.Seconds, "ExpiredTime should be greater than now.");
            Assert(input.FromSender != null, "FromSender should not be null.");
            Assert(input.PublicKey != null, "PublicKey should not be null.");
            Assert(input.RedPacketType != null, "RedPacketType should not be null.");

            var message = $"{input.RedPacketSymbol}-{input.MinAmount}-{input.TotalCount}";
            var messageBytes = HashHelper.ComputeFrom(message).ToByteArray();
            var signature = ByteString.FromBase64(input.RedPacketSignature).ToByteArray();
            var recoverPublicKey = Context.RecoverPublicKey(signature, messageBytes);
            Assert(ByteString.FromBase64(input.PublicKey).ToByteArray() == recoverPublicKey, "Invalid signature.");

            var virtualAddress = Address.FromBase58(HashHelper.ComputeFrom(input.RedPacketId).ToHex());
            Context.SendVirtualInline(Hash.LoadFromHex(input.FromSender.ToBase58()), virtualAddress,
                nameof(State.TokenContract.Transfer),
                new TransferInput
                {
                    To = virtualAddress,
                    Amount = input.TotalAmount,
                    Symbol = input.RedPacketSymbol,
                    Memo = "CreateRedPacket"
                }.ToByteString());
            var redPacket = new RedPacketInfo
            {
                RedPacketId = input.RedPacketId,
                RedPacketType = input.RedPacketType,
                RedPacketSymbol = input.RedPacketSymbol,
                TotalCount = input.TotalCount,
                TotalAmount = input.TotalAmount,
                MinAmount = input.MinAmount,
                ExpirationTime = input.ExpirationTime,
                PublicKey = input.PublicKey,
            };
            State.RedPacketInfoMap[input.RedPacketId] = redPacket;
            Context.Fire(
                new RedPacketCreated
                {
                    FromSender = input.FromSender,
                    RedPacketId = input.RedPacketId,
                    RedPacketType = input.RedPacketType,
                    RedPacketSymbol = input.RedPacketSymbol,
                    TotalCount = input.TotalCount,
                    TotalAmount = input.TotalAmount
                }
            );
            return new Empty();
        }


        public override Empty TransferRedPacket(TransferRedPacketBatchInput input)
        {
            var inputs = input.TransferRedPacketInputs;
            Assert(inputs != null && input.RedPacketId != null, "Invalidate Input");
            var redPacket = State.RedPacketInfoMap[input.RedPacketId];

            var virtualAddressHash = HashHelper.ComputeFrom(input.RedPacketId);
            foreach (var transferRedPacketInput in inputs!)
            {
                var message =
                    $"{redPacket.RedPacketId}-{transferRedPacketInput.ReceiverAddress}-{transferRedPacketInput.Amount}";
                var messageBytes = HashHelper.ComputeFrom(message).ToByteArray();
                var signature = ByteString.FromBase64(transferRedPacketInput.RedPacketSignature).ToByteArray();
                var recoverPublicKey = Context.RecoverPublicKey(signature, messageBytes);
                if (ByteString.FromBase64(redPacket.PublicKey).ToByteArray() == recoverPublicKey)
                {
                    Context.SendVirtualInline(virtualAddressHash, transferRedPacketInput.ReceiverAddress,
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
                        FromSender = redPacket.FromSender,
                        IsSuccess = true
                    });
                }
                else
                {
                    Context.Fire(new RedPacketReceived
                    {
                        RedPacketId = redPacket.RedPacketId,
                        ReceiverAddress = transferRedPacketInput.ReceiverAddress,
                        Amount = transferRedPacketInput.Amount,
                        FromSender = redPacket.FromSender,
                        IsSuccess = false
                    });
                }
            }

            return new Empty();
        }
    }
}