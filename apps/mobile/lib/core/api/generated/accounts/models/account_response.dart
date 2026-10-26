// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'account_status.dart';

part 'account_response.g.dart';

@JsonSerializable()
class AccountResponse {
  const AccountResponse({
    required this.id,
    required this.ownerName,
    required this.email,
    required this.currency,
    required this.balance,
    required this.status,
    required this.createdAt,
  });
  
  factory AccountResponse.fromJson(Map<String, Object?> json) => _$AccountResponseFromJson(json);
  
  final String id;
  final String ownerName;
  final String email;
  final String currency;
  final double balance;
  final AccountStatus status;
  final DateTime createdAt;

  Map<String, Object?> toJson() => _$AccountResponseToJson(this);
}
