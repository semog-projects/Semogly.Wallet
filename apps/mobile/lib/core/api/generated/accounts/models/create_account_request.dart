// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'create_account_request.g.dart';

@JsonSerializable()
class CreateAccountRequest {
  const CreateAccountRequest({
    required this.ownerName,
    required this.document,
    required this.email,
  });
  
  factory CreateAccountRequest.fromJson(Map<String, Object?> json) => _$CreateAccountRequestFromJson(json);
  
  final String ownerName;
  final String document;
  final String email;

  Map<String, Object?> toJson() => _$CreateAccountRequestToJson(this);
}
