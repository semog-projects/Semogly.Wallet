// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'statement_response.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

StatementResponse _$StatementResponseFromJson(Map<String, dynamic> json) =>
    StatementResponse(
      accountId: json['accountId'] as String,
      balance: (json['balance'] as num).toDouble(),
      page: (json['page'] as num).toInt(),
      pageSize: (json['pageSize'] as num).toInt(),
      entries: (json['entries'] as List<dynamic>)
          .map(
            (e) => StatementEntryResponse.fromJson(e as Map<String, dynamic>),
          )
          .toList(),
    );

Map<String, dynamic> _$StatementResponseToJson(StatementResponse instance) =>
    <String, dynamic>{
      'accountId': instance.accountId,
      'balance': instance.balance,
      'page': instance.page,
      'pageSize': instance.pageSize,
      'entries': instance.entries,
    };
