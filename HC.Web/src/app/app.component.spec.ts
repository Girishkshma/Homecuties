import { TestBed } from '@angular/core/testing';
import { RouterModule } from '@angular/router';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { AppComponent } from './app.component';

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [
        RouterModule.forRoot([]),
        // The root component tracks visits through UtilityService, which needs an HttpClient.
        HttpClientTestingModule
      ],
      declarations: [
        AppComponent
      ],
      // The template renders the header/footer components declared by AppModule, which this smoke
      // test does not load.
      schemas: [CUSTOM_ELEMENTS_SCHEMA]
    }).compileComponents();
  });

  // The placeholder 'title' tests that used to live here were left over from the CLI template and
  // broke the whole test build ('AppComponent' has no 'title'), so nothing could be executed.
  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });
});
