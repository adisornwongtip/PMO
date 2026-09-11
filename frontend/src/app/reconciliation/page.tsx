"use client";

import { useState } from "react";
import { UploadCloud, CheckCircle2, AlertCircle, FileSpreadsheet, ArrowUpRight, ShieldCheck } from "lucide-react";

export default function ReconciliationPage() {
  const [batches, setBatches] = useState([
    {
      id: "batch_20260911_01",
      provider: "Opn (Omise)",
      fileName: "opn_settlement_2026-09-11.csv",
      uploadedAt: "Today, 18:30",
      totalRecords: 1250,
      matched: 1242,
      unmatched: 5,
      discrepancy: 3,
      settledAmount: "฿3,842,500.00"
    },
    {
      id: "batch_20260910_02",
      provider: "GB Prime Pay",
      fileName: "gb_settlement_2026-09-10.csv",
      uploadedAt: "Yesterday, 20:15",
      totalRecords: 840,
      matched: 838,
      unmatched: 2,
      discrepancy: 0,
      settledAmount: "฿1,420,100.00"
    }
  ]);

  const sampleDiscrepancies = [
    {
      txnRef: "ORD-89412",
      providerId: "chrg_opn_99812",
      internalAmount: "฿2,500.00",
      statementAmount: "฿2,100.00",
      type: "Amount Mismatch",
      note: "Customer initiated partial refund directly with card issuer."
    },
    {
      txnRef: "ORD-89415",
      providerId: "chrg_opn_99815",
      internalAmount: "฿1,200.00",
      statementAmount: "฿1,200.00",
      type: "Fee Variance",
      note: "Contracted MDR 3.65% (฿43.80), but charged ฿51.20 in settlement report."
    },
    {
      txnRef: "ORD-UNKNOWN",
      providerId: "chrg_opn_ghost_01",
      internalAmount: "Not Found",
      statementAmount: "฿750.00",
      type: "Unmatched in Ledger",
      note: "Settlement entry without corresponding internal order record."
    }
  ];

  return (
    <div className="space-y-6 max-w-7xl mx-auto">
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-white tracking-tight">Automated Reconciliation OS</h1>
          <p className="text-sm text-slate-400">Match transaction ledgers with external PSP settlement reports to eliminate finance discrepancies.</p>
        </div>
      </div>

      {/* Upload Dropzone */}
      <div className="p-8 border-2 border-dashed border-slate-800 hover:border-indigo-500/50 bg-slate-900/30 rounded-2xl flex flex-col items-center justify-center text-center transition-colors cursor-pointer group">
        <div className="p-3 bg-indigo-600/10 rounded-2xl text-indigo-400 group-hover:scale-110 transition-transform mb-3">
          <UploadCloud className="w-8 h-8" />
        </div>
        <h3 className="text-sm font-semibold text-white mb-1">Upload Gateway Settlement CSV / Excel</h3>
        <p className="text-xs text-slate-400 max-w-sm mb-4">
          Drop Opn, GB Prime Pay, 2C2P, or Bank Settlement reports. The engine automatically parses and reconciles against the ledger.
        </p>
        <button className="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-semibold shadow-md shadow-indigo-600/20">
          Select Statement File
        </button>
      </div>

      {/* Reconciliation Batches */}
      <div className="bg-slate-900/40 border border-slate-800 rounded-2xl overflow-hidden">
        <div className="p-4 border-b border-slate-800 bg-slate-950/40">
          <h2 className="text-sm font-semibold text-white">Recent Statement Batches</h2>
        </div>

        <table className="w-full text-left text-xs text-slate-300">
          <thead className="text-[11px] uppercase tracking-wider text-slate-500 border-b border-slate-800 bg-slate-950/20">
            <tr>
              <th className="py-3 px-4">Batch ID</th>
              <th className="py-3 px-4">Provider</th>
              <th className="py-3 px-4">File Name</th>
              <th className="py-3 px-4">Total Records</th>
              <th className="py-3 px-4">Matched</th>
              <th className="py-3 px-4">Discrepancies</th>
              <th className="py-3 px-4">Net Settled Amount</th>
              <th className="py-3 px-4">Uploaded</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800/60">
            {batches.map((b) => (
              <tr key={b.id} className="hover:bg-slate-900/50 transition-colors">
                <td className="py-3.5 px-4 font-mono font-medium text-white">{b.id}</td>
                <td className="py-3.5 px-4 font-semibold text-indigo-300">{b.provider}</td>
                <td className="py-3.5 px-4 text-slate-400 flex items-center gap-1.5">
                  <FileSpreadsheet className="w-3.5 h-3.5 text-emerald-400" />
                  <span>{b.fileName}</span>
                </td>
                <td className="py-3.5 px-4 font-semibold text-white">{b.totalRecords}</td>
                <td className="py-3.5 px-4 text-emerald-400 font-semibold">{b.matched}</td>
                <td className="py-3.5 px-4">
                  {b.discrepancy > 0 ? (
                    <span className="px-2 py-0.5 rounded bg-rose-500/20 text-rose-300 border border-rose-500/30 font-bold">
                      {b.discrepancy} Exceptions
                    </span>
                  ) : (
                    <span className="text-slate-500">0</span>
                  )}
                </td>
                <td className="py-3.5 px-4 font-bold text-white">{b.settledAmount}</td>
                <td className="py-3.5 px-4 text-slate-400">{b.uploadedAt}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Discrepancies & Exceptions Investigation */}
      <div className="p-6 bg-slate-900/40 border border-slate-800 rounded-2xl space-y-4">
        <div>
          <h2 className="text-base font-bold text-white flex items-center gap-2">
            <AlertCircle className="w-5 h-5 text-amber-400" />
            <span>Exceptions Requiring Attention (Current Batch)</span>
          </h2>
          <p className="text-xs text-slate-400">Items where the settlement report differed from internal transaction records.</p>
        </div>

        <div className="space-y-3">
          {sampleDiscrepancies.map((d, idx) => (
            <div key={idx} className="p-4 bg-slate-950/60 border border-slate-800/80 rounded-xl flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3">
              <div>
                <div className="flex items-center gap-2 mb-1">
                  <span className="font-mono text-xs font-bold text-white">{d.txnRef}</span>
                  <span className="text-slate-500 text-xs font-mono">({d.providerId})</span>
                  <span className="px-2 py-0.5 rounded text-[10px] font-semibold bg-amber-500/20 text-amber-300 border border-amber-500/30">
                    {d.type}
                  </span>
                </div>
                <p className="text-xs text-slate-400">{d.note}</p>
              </div>

              <div className="flex items-center gap-4 text-xs">
                <div>
                  <span className="text-slate-500 block text-[10px]">Internal</span>
                  <span className="text-white font-semibold">{d.internalAmount}</span>
                </div>
                <div>
                  <span className="text-slate-500 block text-[10px]">Statement</span>
                  <span className="text-amber-300 font-semibold">{d.statementAmount}</span>
                </div>
                <button className="px-3 py-1 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg text-xs font-medium">
                  Resolve
                </button>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
