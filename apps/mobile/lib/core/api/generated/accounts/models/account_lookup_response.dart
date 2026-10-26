// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'account_lookup_response.g.dart';

@JsonSerializable()
class AccountLookupResponse {
  const AccountLookupResponse({
    required this.accountId,
    required this.displayName,
  });
  
  factory AccountLookupResponse.fromJson(Map<String, Object?> json) => _$AccountLookupResponseFromJson(json);
  
  final String accountId;
  final String displayName;

  Map<String, Object?> toJson() => _$AccountLookupResponseToJson(this);
}
