import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:logging/logging.dart';

import 'app/app.dart';

void main() {
  // Logs de bibliotecas (ex.: SignalR) no console apenas em debug
  if (kDebugMode) {
    Logger.root.level = Level.INFO;
    Logger.root.onRecord.listen((record) => debugPrint('[${record.loggerName}] ${record.message}'));
  }

  runApp(const ProviderScope(child: SemoglyWalletApp()));
}
