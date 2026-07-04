import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'domainExtract', standalone: true, pure: true })
export class DomainExtractPipe implements PipeTransform {
  transform(url: string): string {
    try {
      const host = new URL(url).hostname;
      return host.startsWith('www.') ? host.slice(4) : host;
    } catch {
      return url;
    }
  }
}
