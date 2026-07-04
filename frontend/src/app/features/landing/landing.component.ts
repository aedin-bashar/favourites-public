import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

import { FavIcons } from '../../shared/icons/fav-icons';
import {
  LandingShellComponent,
  LandingSectionComponent,
} from '../../shared/layouts/landing';
import { LandingPreviewComponent } from './landing-preview.component';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [RouterLink, LandingShellComponent, LandingSectionComponent, LandingPreviewComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './landing.component.html',
  styleUrl: './landing.component.scss',
})
export class LandingComponent {
  protected readonly icons = FavIcons;

  protected readonly featureCards = [
    {
      icon: FavIcons.SaveLink,
      title: 'Save in one click',
      copy: 'Paste any URL and store it instantly with a title, tags, and category.',
    },
    {
      icon: FavIcons.Tag,
      title: 'Organize with tags & categories',
      copy: 'Group your saved links with flexible labels and collections that make sense to you.',
    },
    {
      icon: FavIcons.Archived,
      title: 'Archive without losing links',
      copy: "Hide links you don't need right now but aren't ready to delete. Restore anytime.",
    },
    {
      icon: FavIcons.Search,
      title: 'Find anything fast',
      copy: 'Search titles, descriptions, and URLs. Filter by tag, category, or date in seconds.',
    },
  ] as const;
}