"use client";

import { useState } from "react";
import { CheckCircle2, AlertTriangle, ArrowRight, ShieldAlert, Filter, Search } from "lucide-react";

export default function TransactionsPage() {
  const [selectedTxn, setSelectedTxn] = useState<any>(null);

  const transactions = [
    {
      id: "txn_01J8F94821",
      orderRef: "ORD-94821",
      amount: 1500.00,
      currency: "THB",
      method: "PromptPay QR",
      provider: "Opn",
      providerTxnId: "chrg_opn_8317a94f01",
      status: "Success",
      failover: false,
      createdAt: "2026-09-11 22:42:15",
      attempts: [
        { provider: "Opn", status: 200, success: true, latency: 210, message: "Authorized" }
      ]
    },
    {
      id: "txn_01J8F94822",
      orderRef: "ORD-94822",
      amount: 4800.00,
      currency: "THB",
      method: "Credit Card (Visa)",
      provider: "GB Prime Pay",
      providerTxnId: "GB_20260911_4918",
      status: "Success",
      failover: true,
      createdAt: "2026-09-11 22:38:02",
      attempts: [
        { provider: "Opn", status: 504, success: false, latency: 3000, message: "Gateway Timeout: Upstream network unreachable" },
        { provider: "GB Prime Pay", status: 200, success: true, latency: 180, message: "Authorized successfully via automatic failover" }
      ]
    },
    {
      id: "txn_01J8F94823",
      orderRef: "ORD-94823",
      amount: 12500.00,
      currency: "THB",
      method: "Credit Card (Mastercard)",
      provider: "Opn",
      providerTxnId: "chrg_opn_19284ba09",
      status: "Success",
      failover: false,
      createdAt: "2026-09-11 22:25:40",
      attempts: [
        { provider: "Opn", status: 200, success: true, latency: 195, message: "Authorized" }
      ]
    },
    {
      id: "txn_01J8F94825",
      orderRef: "ORD-94825",
      amount: 950.00,
      currency: "THB",
      method: "Credit Card",
      provider: "MockSandbox",
      providerTxnId: null,
      status: "Failed",
      failover: false,
      createdAt: "2026-09-11 22:10:11",
      attempts: [
        { provider: "MockSandbox", status: 400, success: false, latency: 140, message: "Card declined: Insufficient funds (terminal error - no failover)" }
      ]
    }
  ];

  return (
    <div className="space-y-6 max-w-7xl mx-auto">
      <div>
        <h1 className="text-2xl font-bold text-white tracking-tight">Transactions & Routing Inspector</h1>
        <p className="text-sm text-slate-400">Examine how the orchestration engine routes transactions and executes failovers.</p>
      </div>

      {/* Filter Toolbar */}
      <div className="flex flex-col sm:flex-row items-center justify-between gap-4 p-4 bg-slate-900/40 border border-slate-800 rounded-xl">
        <div className="flex items-center gap-2 w-full sm:w-80">
          <Search className="w-4 h-4 text-slate-500" />
          <input
            type="text"
            placeholder="Filter by Order Ref or ID..."
            className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-white focus:outline-none focus:border-indigo-500"
          />
        </div>
        <div className="flex items-center gap-3">
          <button className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-slate-300 bg-slate-800 rounded-lg hover:bg-slate-700">
            <Filter className="w-3.5 h-3.5" />
            <span>All Providers</span>
          </button>
          <button className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-slate-300 bg-slate-800 rounded-lg hover:bg-slate-700">
            <span>Status: All</span>
          </button>
        </div>
      </div>

      {/* Transactions Table */}
      <div className="bg-slate-900/40 border border-slate-800 rounded-2xl overflow-hidden">
        <table className="w-full text-left text-xs text-slate-300">
          <thead className="text-[11px] uppercase tracking-wider text-slate-500 border-b border-slate-800 bg-slate-950/40">
            <tr>
              <th className="py-3.5 px-4">Transaction ID</th>
              <th className="py-3.5 px-4">Order Ref</th>
              <th className="py-3.5 px-4">Amount</th>
              <th className="py-3.5 px-4">Payment Method</th>
              <th className="py-3.5 px-4">Final Provider</th>
              <th className="py-3.5 px-4">Status</th>
              <th className="py-3.5 px-4">Attempts</th>
              <th className="py-3.5 px-4">Action</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800/60">
            {transactions.map((t) => (
              <tr key={t.id} className="hover:bg-slate-900/60 transition-colors">
                <td className="py-3.5 px-4 font-mono text-slate-400">{t.id}</td>
                <td className="py-3.5 px-4 font-mono font-medium text-white">{t.orderRef}</td>
                <td className="py-3.5 px-4 font-semibold text-white">฿{t.amount.toLocaleString("en-US", { minimumFractionDigits: 2 })}</td>
                <td className="py-3.5 px-4">{t.method}</td>
                <td className="py-3.5 px-4">
                  <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-md bg-slate-800 text-slate-200 font-medium">
                    {t.provider}
                    {t.failover && (
                      <span className="text-[9px] bg-amber-500/20 text-amber-300 px-1 rounded font-bold">
                        AUTO-FAILOVER
                      </span>
                    )}
                  </span>
                </td>
                <td className="py-3.5 px-4">
                  {t.status === "Success" ? (
                    <span className="inline-flex items-center gap-1 text-emerald-400 font-medium">
                      <CheckCircle2 className="w-3.5 h-3.5" />
                      Success
                    </span>
                  ) : (
                    <span className="inline-flex items-center gap-1 text-rose-400 font-medium">
                      <AlertTriangle className="w-3.5 h-3.5" />
                      Failed
                    </span>
                  )}
                </td>
                <td className="py-3.5 px-4">
                  <span className="text-xs px-2 py-0.5 rounded bg-slate-800 text-slate-300 font-semibold">
                    {t.attempts.length}
                  </span>
                </td>
                <td className="py-3.5 px-4">
                  <button
                    onClick={() => setSelectedTxn(t)}
                    className="text-xs font-semibold text-indigo-400 hover:text-indigo-300 underline"
                  >
                    View Flow
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Failover Inspector Modal / Drawer */}
      {selectedTxn && (
        <div className="fixed inset-0 bg-black/70 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-2xl p-6 space-y-6 shadow-2xl">
            <div className="flex items-center justify-between border-b border-slate-800 pb-4">
              <div>
                <h3 className="text-base font-bold text-white flex items-center gap-2">
                  <span>Routing Execution Timeline</span>
                  {selectedTxn.failover && (
                    <span className="text-xs bg-amber-500/20 text-amber-300 px-2 py-0.5 rounded font-semibold border border-amber-500/30">
                      Automatic Failover Triggered
                    </span>
                  )}
                </h3>
                <p className="text-xs text-slate-400 font-mono mt-0.5">{selectedTxn.id} &bull; {selectedTxn.orderRef}</p>
              </div>
              <button
                onClick={() => setSelectedTxn(null)}
                className="text-slate-400 hover:text-white text-sm font-semibold p-1"
              >
                &times;
              </button>
            </div>

            {/* Timeline Steps */}
            <div className="space-y-4">
              <h4 className="text-xs font-semibold uppercase tracking-wider text-slate-400">Step-by-Step Provider Attempts</h4>
              <div className="space-y-3">
                {selectedTxn.attempts.map((att: any, idx: number) => (
                  <div 
                    key={idx} 
                    className={`p-4 rounded-xl border ${att.success ? "bg-emerald-950/20 border-emerald-800/40" : "bg-rose-950/20 border-rose-800/40"}`}
                  >
                    <div className="flex items-center justify-between mb-1">
                      <span className="font-semibold text-white text-xs flex items-center gap-2">
                        <span>Attempt {idx + 1}: Provider {att.provider}</span>
                        <span className={`px-1.5 py-0.5 rounded text-[10px] font-bold ${att.success ? "bg-emerald-500/20 text-emerald-300" : "bg-rose-500/20 text-rose-300"}`}>
                          HTTP {att.status}
                        </span>
                      </span>
                      <span className="text-[11px] text-slate-400 font-mono">{att.latency}ms latency</span>
                    </div>
                    <p className="text-xs text-slate-300">{att.message}</p>
                  </div>
                ))}
              </div>
            </div>

            <div className="pt-4 border-t border-slate-800 flex justify-end">
              <button
                onClick={() => setSelectedTxn(null)}
                className="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-white rounded-lg text-xs font-semibold"
              >
                Close Inspector
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
