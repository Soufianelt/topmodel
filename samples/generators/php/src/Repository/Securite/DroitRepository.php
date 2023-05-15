<?php
////
//// ATTENTION CE FICHIER EST GENERE AUTOMATIQUEMENT !
////

namespace App\Repository\Securite;

use App\Entity\Securite\Droit;
use Doctrine\Bundle\DoctrineBundle\Repository\ServiceEntityRepository;
use Symfony\Bridge\Doctrine\ManagerRegistry;

/**
 * @extends ServiceEntityRepository<Droit>
 */
class DroitRepository extends ServiceEntityRepository
{
  public function __construct(ManagerRegistry $registry)
  {
    parent::__construct($registry, Droit::class);
  }
}