using System.Collections.Generic;
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
        public override Empty Initialize(InitializeInput input)
        {
            Assert(!State.Initialized.Value, "Already initialized.");
            State.Admin.Value = input.ContractAdmin ?? Context.Sender;
            State.RedPacketMaxCount.Value = input.MaxCount;
            State.TokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
            State.Initialized.Value = true;
            return new Empty();
        }

        public override Empty CreateRedPacket(CreateRedPacketInput input)
        {
            Assert(input.RedPacketId != null, "RedPacketId should not be null.");
            Assert(State.RedPacketInfoMap[input.RedPacketId] == null, "RedPacketId already exists.");
            Assert(input.TotalAmount > 0, "TotalAmount should be greater than 0.");
            Assert(input.TotalCount > 0, "TotalCount should be greater than 0.");
            Assert(State.RedPacketMaxCount.Value >= input.TotalCount,
                "TotalCount should be less than or equal to MaxCount.");
            Assert(input.MinAmount > 0, "MinAmount should be greater than 0.");
            Assert(input.RedPacketSymbol != null, "RedPacketSymbol should not be null.");
            Assert(input.TotalAmount > input.MinAmount * input.TotalCount,
                "TotalAmount should be greater than MinAmount * TotalCount.");
            Assert(input.ExpirationTime > Context.CurrentBlockTime.Seconds, "ExpiredTime should be greater than now.");
            Assert(input.FromSender != null, "FromSender should not be null.");
            Assert(input.PublicKey != null, "PublicKey should not be null.");

            var maxCount = State.RedPacketMaxCount.Value;

            var message = $"{input.RedPacketSymbol}-{input.MinAmount}-{maxCount}";
            var messageBytes = HashHelper.ComputeFrom(message).ToByteArray();
            var signature = ByteStringHelper.FromHexString(input.RedPacketSignature);
            var recoverPublicKey = Context.RecoverPublicKey(signature.ToByteArray(), messageBytes).ToHex();
            var bytes = ByteStringHelper.FromHexString(input.PublicKey).ToByteArray().ToHex();

            Assert(bytes == recoverPublicKey, "Invalid signature.");
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
            Assert(redPacket != null, "RedPacket not exists.");
            var virtualAddressHash = HashHelper.ComputeFrom(input.RedPacketId);
            foreach (var transferRedPacketInput in inputs!)
            {
                var list = State.AlreadySnatchedList[transferRedPacketInput.RedPacketId] != null
                    ? State.AlreadySnatchedList[transferRedPacketInput.RedPacketId]
                    : new List<Address>();
                if (list.Contains(transferRedPacketInput.ReceiverAddress))
                {
                    continue;
                }

                var message =
                    $"{redPacket.RedPacketId}-{transferRedPacketInput.ReceiverAddress}-{transferRedPacketInput.Amount}";
                var messageBytes = HashHelper.ComputeFrom(message).ToByteArray();
                var signature = ByteStringHelper.FromHexString(transferRedPacketInput.RedPacketSignature).ToByteArray();
                var recoverPublicKey = Context.RecoverPublicKey(signature, messageBytes).ToHex();
                var pubBytes = ByteStringHelper.FromHexString(redPacket.PublicKey).ToByteArray().ToHex();
                if (recoverPublicKey == pubBytes)
                {
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
                        FromSender = redPacket.FromSender,
                        IsSuccess = true
                    });
                    list.Add(transferRedPacketInput.ReceiverAddress);
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

        public override RedPacketOutput GetRedPacketInfo(GetRedPacketInput input)
        {
            var packetInfo = State.RedPacketInfoMap[input.RedPacketId];
            Assert(packetInfo != null, "RedPacketId not exists.");
            return new RedPacketOutput
            {
                RedPacketId = input.RedPacketId,
                RedPacketType = packetInfo.RedPacketType,
                RedPacketSymbol = packetInfo.RedPacketSymbol,
                TotalCount = packetInfo.TotalCount,
                TotalAmount = packetInfo.TotalAmount,
                MinAmount = packetInfo.MinAmount,
                ExpirationTime = packetInfo.ExpirationTime,
                PublicKey = packetInfo.PublicKey,
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
    }
}