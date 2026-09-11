"use client";

import { useEffect, useState } from "react";
import { Bell, Search, User, Command, Zap, ArrowUpRight } from "lucide-react";

interface HeaderProps {
  onOpenCommandPalette?: () => void;
}

export function Header({ onOpenCommandPalette }: HeaderProps) {
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key === "k") {
        e.preventDefault();
        onOpenCommandPalette?.();
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [onOpenCommandPalette]);

  return (
    <header className="h-16 border-b border-white/[0.08] bg-[#0B0F17]/80 backdrop-blur-xl px-6 flex items-center justify-between text-slate-200 sticky top-0 z-30">
      <div className="flex items-center gap-4 w-96">
        <button
          type="button"
          onClick={onOpenCommandPalette}
          className="w-full bg-slate-900/60 border border-white/[0.08] hover:border-indigo-500/40 rounded-xl px-3.5 py-2 text-xs text-slate-400 flex items-center justify-between group transition-all duration-200 shadow-inner"
        >
          <div className="flex items-center gap-2.5">
            <Search className="w-3.5 h-3.5 text-slate-400 group-hover:text-indigo-400 transition-colors" />
            <span className="text-slate-400 group-hover:text-slate-200">Search transactions, rules, or jump to...</span>
          </div>
          <kbd className="hidden sm:inline-flex items-center gap-0.5 font-mono text-[10px] bg-slate-800/80 text-slate-400 px-1.5 py-0.5 rounded border border-white/[0.08]">
            <Command className="w-2.5 h-2.5" /> K
          </kbd>
        </button>
      </div>

      <div className="flex items-center gap-4">
        <div className="hidden md:flex items-center gap-2 px-2.5 py-1 rounded-full bg-emerald-500/10 border border-emerald-500/20 text-emerald-400 text-xs font-medium">
          <span className="relative flex h-2 w-2">
            <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
            <span className="relative inline-flex rounded-full h-2 w-2 bg-emerald-500"></span>
          </span>
          <span>Orchestration Active</span>
        </div>

        <button className="p-2 text-slate-400 hover:text-slate-200 hover:bg-slate-900/60 rounded-xl transition-colors relative border border-transparent hover:border-white/[0.06]">
          <Bell className="w-4 h-4" />
          <span className="w-2 h-2 bg-indigo-500 rounded-full absolute top-1.5 right-1.5 ring-2 ring-[#0B0F17]" />
        </button>

        <div className="flex items-center gap-3 pl-4 border-l border-white/[0.08]">
          <div className="w-8 h-8 rounded-xl bg-gradient-to-tr from-indigo-600 to-violet-500 border border-indigo-400/30 flex items-center justify-center text-white font-bold text-xs shadow-md shadow-indigo-500/20">
            PF
          </div>
          <div className="text-left hidden sm:block">
            <div className="text-xs font-semibold text-white tracking-tight">Demo Enterprise Co.</div>
            <div className="text-[10px] font-mono text-slate-400">pk_live_payflow...</div>
          </div>
        </div>
      </div>
    </header>
  );
}
