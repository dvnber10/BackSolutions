namespace BackSolutions.Core.Enums;

/// <summary>
/// Tipo de bloque visual dentro de una página. El campo <c>PageSection.ContentJson</c>
/// guarda el payload con la forma que cada tipo define.
/// </summary>
public enum PageSectionType
{
    Hero = 0,
    RichText = 1,
    Features = 2,
    Stats = 3,
    ServicesGrid = 4,
    ProjectsGrid = 5,
    BlogGrid = 6,
    Testimonials = 7,
    Faq = 8,
    Gallery = 9,
    LogoCloud = 10,
    CallToAction = 11
}
