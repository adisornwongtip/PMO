"use client";

import { useState } from "react";
import { Sidebar } from "@/components/Sidebar";
import { Header } from "@/components/Header";
import { CommandPalette } from "@/components/CommandPalette";

export function PortalShell({ children }: { children: React.ReactNode }) {
  const [isCommandOpen, setIsCommandOpen] = useState(false);

  return (
    <div className="flex min-h-screen bg-[#080B11] text-slate-100 font-sans selection:bg-indigo-500 selection:text-white">
      <Sidebar />
      <div className="flex-1 flex flex-col min-w-0">
        <Header onOpenCommandPalette={() => setIsCommandOpen(true)} />
        <main className="flex-1 p-6 sm:p-8 overflow-y-auto relative">
          {/* Subtle Linear-style top ambient glow */}
          <div className="pointer-events-none absolute -top-24 left-1/2 -translate-x-1/2 w-[800px] h-[350px] bg-gradient-to-b from-indigo-500/10 via-violet-500/5 to-transparent blur-3xl rounded-full" />
          <div className="relative z-10">
            {children}
          </div>
        </main>
      </div>
      <CommandPalette isOpen={isCommandOpen} onClose={() => setIsCommandOpen(false)} />
    </div>
  );
}
