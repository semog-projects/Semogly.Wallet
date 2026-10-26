// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'create_transfer_request.g.dart';

@JsonSerializable()
class CreateTransferRequest {
  const CreateTransferRequest({
    required this.sourceAccountId,
    required this.destinationAccountId,
    required this.amount,
  });
  
  factory CreateTransferRequest.fromJson(Map<String, Object?> json) => _$CreateTransferRequestFromJson(json);
  
  final String sourceAccountId;
  final String destinationAccountId;
  final double amount;

  Map<String, Object?> toJson() => _$CreateTransferRequestToJson(this);
}
