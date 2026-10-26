// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'ledger_book_response.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

LedgerBookResponse _$LedgerBookResponseFromJson(Map<String, dynamic> json) =>
    LedgerBookResponse(
      cashIn: (json['cashIn'] as num).toDouble(),
      clearing: (json['clearing'] as num).toDouble(),
      customerBalances: (json['customerBalances'] as num).toDouble(),
      customerAccounts: (json['customerAccounts'] as num).toInt(),
      total: (json['total'] as num).toDouble(),
      isBalanced: json['isBalanced'] as bool?,
    );

Map<String, dynamic> _$LedgerBookResponseToJson(LedgerBookResponse instance) =>
    <String, dynamic>{
      'cashIn': instance.cashIn,
      'clearing': instance.clearing,
      'customerBalances': instance.customerBalances,
      'customerAccounts': instance.customerAccounts,
      'total': instance.total,
      'isBalanced': instance.isBalanced,
    };
