import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-footer',
  standalone: true,
  imports: [RouterModule],
  template: `
    <footer class="bg-surface-container-lowest py-16 border-t border-outline-variant/10 mt-auto">
        <div class="container mx-auto px-8">
            <div class="flex flex-col md:flex-row justify-between items-start gap-12">
                <div class="max-w-sm">
                    <span class="font-serif italic text-3xl text-primary mb-6 block">SkyHorizon</span>
                    <p class="text-on-surface-variant text-sm leading-relaxed mb-8">Providing unparalleled aerial
                        experiences through our global fleet. Every flight is a masterwork of precision
                        and luxury.</p>
                    <div class="flex gap-4">
                        <a class="text-secondary/40 hover:text-primary transition-colors cursor-pointer"><span
                                class="material-symbols-outlined">public</span></a>
                        <a class="text-secondary/40 hover:text-primary transition-colors cursor-pointer"><span
                                class="material-symbols-outlined">mail</span></a>
                        <a class="text-secondary/40 hover:text-primary transition-colors cursor-pointer"><span
                                class="material-symbols-outlined">phone_in_talk</span></a>
                    </div>
                </div>
                <div class="grid grid-cols-2 lg:grid-cols-3 gap-12">
                    <div>
                        <h5 class="font-label text-xs tracking-widest uppercase text-on-surface mb-6">Fleet</h5>
                        <ul class="space-y-4 text-sm text-on-surface-variant font-light">
                            <li class="hover:text-primary transition-colors"><a class="cursor-pointer">Long Range</a></li>
                            <li class="hover:text-primary transition-colors"><a class="cursor-pointer">Super Mid-Size</a></li>
                            <li class="hover:text-primary transition-colors"><a class="cursor-pointer">Light Jets</a></li>
                        </ul>
                    </div>
                    <div>
                        <h5 class="font-label text-xs tracking-widest uppercase text-on-surface mb-6">Experience</h5>
                        <ul class="space-y-4 text-sm text-on-surface-variant font-light">
                            <li class="hover:text-primary transition-colors"><a class="cursor-pointer">Culinary Art</a></li>
                            <li class="hover:text-primary transition-colors"><a class="cursor-pointer">Ground Services</a></li>
                            <li class="hover:text-primary transition-colors"><a class="cursor-pointer">Global Terminals</a></li>
                        </ul>
                    </div>
                    <div>
                        <h5 class="font-label text-xs tracking-widest uppercase text-on-surface mb-6">Legal</h5>
                        <ul class="space-y-4 text-sm text-on-surface-variant font-light">
                            <li class="hover:text-primary transition-colors"><a class="cursor-pointer">Privacy Policy</a></li>
                            <li class="hover:text-primary transition-colors"><a class="cursor-pointer">Terms of Service</a></li>
                            <li class="hover:text-primary transition-colors"><a class="cursor-pointer">Cookie Settings</a></li>
                        </ul>
                    </div>
                </div>
            </div>
            <div class="mt-20 pt-8 border-t border-outline-variant/10 flex flex-col md:flex-row justify-between items-center gap-4">
                <span class="text-[10px] font-label tracking-widest uppercase text-on-surface-variant">© {{ currentYear }} SkyHorizon
                    Aviation Group. All Rights Reserved.</span>
                <span class="text-[10px] font-label tracking-widest uppercase text-on-surface-variant">Operated under
                    Part 135 Certification</span>
            </div>
        </div>
    </footer>
  `
})
export class Footer {
  currentYear = new Date().getFullYear();
}
