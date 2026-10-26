// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'deposit_request.g.dart';

@JsonSerializable()
class DepositRequest {
  const DepositRequest({
    required this.amount,
  });
  
  factory DepositRequest.fromJson(Map<String, Object?> json) => _$DepositRequestFromJson(json);
  
  final double amount;

  Map<String, Object?> toJson() => _$DepositRequestToJson(this);
}
