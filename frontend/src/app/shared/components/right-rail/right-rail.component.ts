import { Component } from '@angular/core';

@Component({
  selector: 'fav-right-rail',
  standalone: true,
  template: `
    <aside class="right-rail">
      <ng-content></ng-content>
    </aside>
  `,
  styleUrl: './right-rail.component.scss',
})
export class RightRailComponent {}
